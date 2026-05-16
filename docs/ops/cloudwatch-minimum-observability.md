# CloudWatch 최소 관측성

AWS dev 토폴로지에서 control plane(EC2 spring) 과 room-server (Fargate) 를 한 곳에서 추적하기 위한 최소 셋업.

> Grafana / Prometheus 도입은 본 plan 범위 밖. CloudWatch Logs + custom metrics + alarms 만으로 시작한다.

---

## 1. Logs

### Log groups

| Log group | 소스 | 보관 기간 |
|---|---|---|
| `/ec2/murang-spring` | EC2 의 `spring` 컨테이너 stdout/stderr | 14 일 |
| `/ec2/murang-mariadb` | EC2 의 `mariadb` 컨테이너 stdout/stderr (선택) | 7 일 |
| `/ecs/murang-room-server` | Fargate task 의 container stdout/stderr | 14 일 |

### EC2 → CloudWatch Logs

`amazon-cloudwatch-agent` 설치 또는 awslogs Docker logging driver 사용. 간단히:

```bash
sudo apt-get install -y amazon-cloudwatch-agent
```

`/opt/aws/amazon-cloudwatch-agent/etc/amazon-cloudwatch-agent.json`:

```json
{
  "logs": {
    "logs_collected": {
      "files": {
        "collect_list": [
          {
            "file_path": "/var/lib/docker/containers/*/*-json.log",
            "log_group_name": "/ec2/murang-spring",
            "log_stream_name": "{instance_id}/spring"
          }
        ]
      }
    }
  }
}
```

또는 더 간단히 docker-compose 의 spring 서비스에 `logging.driver: awslogs` 추가 (EC2 가 awslogs driver 지원 시).

### Fargate → CloudWatch Logs

Task definition 의 container 정의에 `logConfiguration`:

```json
"logConfiguration": {
  "logDriver": "awslogs",
  "options": {
    "awslogs-group": "/ecs/murang-room-server",
    "awslogs-region": "ap-northeast-2",
    "awslogs-stream-prefix": "room"
  }
}
```

---

## 2. Custom metrics

`RoomServerManager` 가 다음 metric 을 발행. 네임스페이스 `Murang/Room`.

| 코드 측 메터 이름 | CloudWatch 측 발행 이름 | Unit | 의미 |
|---|---|---|---|
| `active_room_count` | `active_room_count.value` | Count | 현재 READY+ACTIVE 인스턴스 수 (1분 주기 gauge) |
| `room_provision_latency` | (후속) | Milliseconds | provision 요청 → READY 까지 시간 |
| `room_ready_latency` | (후속) | Milliseconds | RunTask 호출 → ready callback 까지 시간 |
| `unhealthy_termination_count` | (후속) | Count | UNHEALTHY → TERMINATED 전이 횟수 |
| `admission_rejection_count` | (후속) | Count | RoomFull/WrongPassword/RoomNotFound 거절 횟수 |
| `run_task_failure_count` | (후속) | Count | ECS RunTask 실패 횟수 (capacity/AccessDenied 등) |

> Micrometer 의 `CloudWatchMeterRegistry` 는 gauge 에 `.value` 접미사를, counter/timer 에는 `.count` / `.sum` / `.avg` / `.max` 접미사를 자동 추가한다. `aws cloudwatch list-metrics --namespace Murang/Room` 으로 확인할 때는 접미사 포함 이름을 사용한다.
>
> 본 plan 의 acceptance criteria 는 "최소 1개 custom metric(`active_room_count` 또는 `room_ready_latency`) 발행" 만 요구한다. 나머지는 후속.

발행 방식: Spring Boot 3.4 의 actuator-autoconfigure 에서 `CloudWatchMetricsExportAutoConfiguration` 이 빠져 있어 (`AutoConfiguration.imports` 확인 결과), [`backend/.../observability/CloudWatchMetricsConfiguration.java`](../../backend/src/main/java/com/murang/room/observability/CloudWatchMetricsConfiguration.java) 가 `CloudWatchAsyncClient` + `CloudWatchConfig` + `CloudWatchMeterRegistry` 빈을 직접 등록한다. Spring 의 `CompositeMeterRegistryAutoConfiguration` 이 합쳐주므로 `MeterRegistry` 에 등록한 메터는 모두 CloudWatch 로 publish 된다.

---

## 3. Alarms

| Alarm | 조건 | 액션 |
|---|---|---|
| `RunTaskFailure` | `run_task_failure_count >= 1` (5분) | SNS topic `murang-ops` |
| `ReadyTimeout` | `room_provision_latency p95 > 120s` (10분) | SNS topic `murang-ops` |
| `UnhealthySurge` | `unhealthy_termination_count >= 3` (10분) | SNS topic `murang-ops` |
| `EcsCapacity` | ECS service event 의 `service capacity` 또는 RunTask failure with `RESOURCE:*` 비율 급증 | SNS topic `murang-ops` |

SNS topic 의 구독자(이메일 / Slack webhook)는 운영자가 별도로 등록.

---

## 4. 본 plan AC 매핑

- `[manual-hard]` CloudWatch Logs 에서 Spring 로그와 room-server task 로그를 확인할 수 있다 → §1
- `[manual-hard]` 최소 1개 custom metric 이 발행된다 (`active_room_count` 또는 `room_ready_latency`) → §2

---

## 5. 의도적으로 빠진 것

- X-Ray / OpenTelemetry 분산 트레이싱
- Datadog / Grafana 연동
- 비용 알람 (`AWS Budgets`)
- 로그 기반 anomaly detection

후속 plan 에서 채운다.
