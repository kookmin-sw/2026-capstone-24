# MU:RANG Backend

## 로컬 개발 DB

Windows에 설치된 MariaDB 10.11+는 `root` 계정에 `mysql_native_password` 외에 `gssapi`(SSPI) fallback이 함께 들어갈 수 있습니다. 이 상태에서 JDBC나 CLI가 잘못된 비밀번호 또는 무비밀번호로 `root` 접속을 시도하면 Windows SSPI 인증으로 넘어가면서 원인 파악이 어려워집니다.

앱에서는 `root`를 쓰지 말고 전용 계정 `murang_app`을 사용하세요.

### 1. root로 한 번만 실행할 SQL

```sql
CREATE DATABASE IF NOT EXISTS murang;

CREATE USER IF NOT EXISTS 'murang_app'@'localhost' IDENTIFIED BY 'change-me';
CREATE USER IF NOT EXISTS 'murang_app'@'127.0.0.1' IDENTIFIED BY 'change-me';

GRANT ALL PRIVILEGES ON murang.* TO 'murang_app'@'localhost';
GRANT ALL PRIVILEGES ON murang.* TO 'murang_app'@'127.0.0.1';

FLUSH PRIVILEGES;
```

### 2. 앱 실행 환경변수

```powershell
$env:SPRING_PROFILES_ACTIVE = "dev"
$env:DB_URL = "jdbc:mariadb://localhost:3306/murang?sslMode=disable"
$env:DB_USERNAME = "murang_app"
$env:DB_PASSWORD = "change-me"
$env:MURANG_JWT_SECRET = "test-secret-for-local-dev-must-be-at-least-sixty-four-bytes-long-2026"
```

반복 실행이 번거로우면 아래처럼 스크립트를 사용할 수 있습니다.

```powershell
powershell -ExecutionPolicy Bypass -File .\run-dev.ps1
```

### 3. root 계정 상태를 확인하고 싶을 때

```sql
SHOW CREATE USER 'root'@'localhost';
SHOW CREATE USER 'root'@'127.0.0.1';
```

출력에 `gssapi` 또는 `IDENTIFIED VIA ... OR gssapi`가 보이면, 그 계정은 Windows SSPI fallback이 열려 있는 상태입니다. 앱 계정은 이런 fallback 없이 비밀번호 기반으로 따로 만드는 편이 안전합니다.

### 4. 로컬 API 검증

백엔드가 떠 있는 상태에서 아래 스크립트를 실행하면 `health`, `meta-login`, `users/me`, 같은 Meta 계정 재로그인 시 `userId` 유지, 다른 Meta 계정의 닉네임 충돌까지 한 번에 확인할 수 있습니다.

```powershell
powershell -ExecutionPolicy Bypass -File .\verify-local-auth.ps1
```
