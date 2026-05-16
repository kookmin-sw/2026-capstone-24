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

| Alarm name | CloudWatch metric (실제 발행 이름) | 조건 | Statistic | 윈도우 | 액션 |
|---|---|---|---|---|---|
| `murang-room-RunTaskFailure` | `run_task_failure_count.count` | `>= 1` | Sum | 5분 | SNS `murang-ops` |
| `murang-room-ReadyTimeout` | `room_provision_latency.max` | `> 120000` (ms) | Maximum | 10분 | SNS `murang-ops` |
| `murang-room-UnhealthySurge` | `unhealthy_termination_count.count` | `>= 3` | Sum | 10분 | SNS `murang-ops` |
| `murang-room-EcsCapacity` | `run_task_failure_count.count` | `>= 5` | Sum | 10분 | SNS `murang-ops` |

> 메트릭 이름은 §2 의 `.count` / `.max` 접미사 규약과 동일. `room_provision_latency` 는 Micrometer Timer 라 `.count`/`.sum`/`.avg`/`.max` 가 발행되며 p95 percentile 은 별도 publisher 설정이 필요해 본 단계에선 `.max` fallback 을 사용한다.
>
> `EcsCapacity` 는 plan 원안의 "ECS service event 의 service capacity" 가 CloudWatch metric 으로 직접 노출되지 않아, capacity 부족 시에도 결국 `RunTask` 가 실패하는 점을 이용해 `run_task_failure_count.count >= 5/10min` 임계로 fallback 한다. 전용 capacity metric 도입 시 별도 alarm 으로 분리.

### 적용 절차

```bash
# 1) SNS topic 생성 (한 번만)
aws sns create-topic --name murang-ops --region ap-northeast-2
aws sns subscribe --topic-arn arn:aws:sns:ap-northeast-2:<account>:murang-ops \
  --protocol email --notification-endpoint ops@example.com

# 2) 4종 alarm 일괄 생성/갱신 (멱등)
tools/create-cloudwatch-alarms.sh                                # Linux/macOS
.\tools\create-cloudwatch-alarms.ps1                             # Windows
# 옵션:
#   tools/create-cloudwatch-alarms.sh --help 는 없음. 대신 환경변수:
#     REGION=ap-northeast-2 SNS_TOPIC_ARN=arn:... tools/create-cloudwatch-alarms.sh

# 3) 확인
aws cloudwatch describe-alarms --alarm-name-prefix murang-room- --region ap-northeast-2 \
  --query "MetricAlarms[].{name:AlarmName, state:StateValue, metric:MetricName, threshold:Threshold}"
```

> 현재 publish 되는 custom metric 은 `active_room_count.value` 한 종류뿐이므로, 위 alarm 들은 metric 이 추가 publish 되기 전까지 `INSUFFICIENT_DATA` 상태로 머문다. 정상 동작.

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
