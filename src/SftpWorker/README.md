# SftpWorker

A robust, configurable .NET worker service for ingesting and processing files from an SFTP server, parsing CSV content, and forwarding structured data to an API endpoint.  
Includes comprehensive unit tests, test coverage reporting, and a Dockerized local SFTP mock for development and CI.

---

## Table of Contents

- [Overview](#overview)
- [Quick Start](#quick-start)
- [Configuration & Environment Variables](#configuration--environment-variables)
- [Running the Worker](#running-the-worker)
- [SFTP Mock Container for Local Development](#sftp-mock-container-for-local-development)
- [Testing & Coverage](#testing--coverage)
- [Common Commands](#common-commands)
- [Project Structure](#project-structure)
- [Contributing](#contributing)
- [FAQ](#faq)

---

## Overview

**SftpWorker** regularly polls a configured SFTP server, moves files for processing, parses CSV data, and sends the records to a downstream API.  
It is designed for reliability, observability (rich logging), and testability (with extensive unit tests and mock support).

---

## Quick Start

1. **Clone the repository:**
   ```sh
   git clone https://github.com/your-org/SftpWorker.git
   cd SftpWorker
   ```

2. **Set up the environment:**
   - Copy `.env.example` to `.env` and edit as needed.

3. **(Optional) Start the SFTP mock container:**
   ```sh
   docker compose up -d sftp
   ```

4. **Build and run the worker:**
   ```sh
   dotnet build
   dotnet run --project src/SftpWorker
   ```

---

## Configuration & Environment Variables

All configuration can be supplied via environment variables or `appsettings.json`.  
**Key environment variables:**

| Variable                | Description                                   | Example                |
|-------------------------|-----------------------------------------------|------------------------|
| SFTP_HOST               | SFTP server hostname                          | localhost              |
| SFTP_PORT               | SFTP server port                              | 2222                   |
| SFTP_USERNAME           | SFTP username                                 | foo                    |
| SFTP_PASSWORD           | SFTP password                                 | pass                   |
| SFTP_REMOTE_PATH        | Path to poll for files                        | /upload                |
| SFTP_ARCHIVE_PATH       | Path to move processed files                  | /archive               |
| API_BASE_URL            | URL of the target API                         | http://localhost:8080  |
| API_KEY                 | API authentication key/token                  | myapikey               |
| POLL_INTERVAL_SECONDS   | Polling interval in seconds                   | 10                     |

**Create a `.env` file (or use `appsettings.Development.json`) for local development.**  
See `.env.example` for a template.

---

## Running the Worker

Build and run with the standard .NET CLI:

```sh
dotnet build
dotnet run --project src/SftpWorker
```

Or use Docker:

```sh
docker build -t sftpworker .
docker run --env-file .env sftpworker
```

---

## SFTP Mock Container for Local Development

A Docker Compose service is provided for a fast SFTP testing environment.

**Start the SFTP mock:**
```sh
docker compose up -d sftp
```

**Configuration Tips:**
- The mock SFTP container starts up quickly for integration tests.
- Default credentials: `foo` / `pass`
- Port: `2222`
- You can mount local folders or seed files as needed in `docker-compose.yml`.

**To stop:**  
```sh
docker compose down
```

---

## Testing & Coverage

### Run all unit tests

```sh
dotnet test
```

### Test Coverage (XML and HTML)

**1. Cobertura XML:**
```sh
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=./TestResults/coverage.xml
```

**2. HTML Report:**

- **Install ReportGenerator:**
  ```sh
  dotnet tool install -g dotnet-reportgenerator-globaltool
  ```
- **Generate HTML:**
  ```sh
  reportgenerator -reports:./TestResults/coverage.xml -targetdir:./TestResults/CoverageReport -reporttypes:Html
  ```
- Open `./TestResults/CoverageReport/index.html` in your browser.

---

## Common Commands

- **Build:**  
  `dotnet build`

- **Run (local):**  
  `dotnet run --project src/SftpWorker`

- **Run (Docker):**  
  `docker build -t sftpworker . && docker run --env-file .env sftpworker`

- **Unit tests:**  
  `dotnet test`

- **Test coverage (XML):**  
  `dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=./TestResults/coverage.xml`

- **Coverage HTML (after XML):**  
  `reportgenerator -reports:./TestResults/coverage.xml -targetdir:./TestResults/CoverageReport -reporttypes:Html`

- **Start SFTP mock:**  
  `docker compose up -d sftp`

- **Stop SFTP mock:**  
  `docker compose down`

---

## Project Structure

```
SftpWorker/
├── src/
│   └── SftpWorker/          # Worker implementation
├── tests/
│   └── SftpWorker.Tests/    # Unit tests
├── docker-compose.yml       # For SFTP mock container
├── .env.example             # Environment variable template
├── README.md
```