# ── Build stage ──────────────────────────────────────────────────────────────
FROM gradle:8-jdk21-jammy AS build

WORKDIR /workspace

# Gradle 캐시 레이어를 최대한 분리하기 위해 빌드 스크립트를 먼저 복사한다.
COPY build.gradle settings.gradle ./
COPY gradle/ gradle/
COPY gradlew gradlew.bat ./

# Windows에서 git core.autocrlf 로 CRLF 변환된 wrapper 의 line ending 정리 + 실행 권한 부여
RUN sed -i 's/\r$//' gradlew && chmod +x gradlew

# 의존성 다운로드 (소스 없음 → 캐시 레이어 재사용)
RUN ./gradlew dependencies --no-daemon -q || true

# 소스 복사 후 jar 빌드 (테스트 제외)
COPY src/ src/

RUN ./gradlew bootJar -x test --no-daemon

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM eclipse-temurin:21-jre AS runtime

WORKDIR /app

COPY --from=build /workspace/build/libs/*.jar /app/app.jar

EXPOSE 8080

ENTRYPOINT ["java", "-jar", "/app/app.jar"]
