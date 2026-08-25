# Industrial Telemetry Platform

A .NET 10, MQTT and Blazor demonstration for simulated industrial telemetry, alert classification and durable append-only event capture.

## Features

- MQTT ingestion and publishing with at-least-once delivery.
- Local loopback mode that runs without a broker.
- Temperature, vibration and pressure threshold evaluation.
- Bounded in-memory queries with optional JSON Lines persistence.
- Interactive Blazor dashboard and simulation control.
- Health and REST telemetry endpoints.
- Docker Compose environment with Eclipse Mosquitto.
- Dependency-free executable domain tests.

This project uses simulated equipment and protocols. It does not claim production SCADA, OPC UA, Modbus or industrial commissioning experience.

## Run without MQTT

```bash
dotnet run --project src/Telemetry.Web/Telemetry.Web.csproj
```

Open the displayed local URL and use **Simulate reading**. To enable MQTT and durable container storage:

```bash
docker compose up --build
```

Open `http://localhost:8087`. The included broker allows anonymous access only for this isolated local demonstration; it is not a production configuration.

## Test

```bash
dotnet run --project tests/Telemetry.Tests/Telemetry.Tests.csproj
```

## Next integrations

- OPC UA and Modbus protocol adapters behind the existing ingestion boundary.
- Time-series storage and retention policies.
- OpenTelemetry metrics and device-fleet alert routing.
