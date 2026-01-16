# Telemetry

Telemetry of Cirrus is implemented using [OpenTelemetry](https://opentelemetry.io/). It collects traces of operations performed by Cirrus and exports them to Azure Application Insights using the [Azure Monitor OpenTelemetry Exporter](https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-overview).

## Collection

Telemetry is collected using `ActivitySource` instances defined in `ITelemetryService`. Core libraries and extensions can obtain the `ITelemetryService` from dependency injection and create activities to trace operations. For example:

```csharp
var activitySource = serviceProvider.GetService<ITelemetryService>()?.ActivitySource;
using var activity = activitySource?.StartActivity("OperationName");
// business logic here
```

## Export

### Azure Application Insights

Telemetry is exported to Azure Application Insights using the `AzureMonitorTraceExporter`.

For now the connection string is hardcoded to a specific Application Insights resource. In the future, this can be made configurable.

To visit the Application Insights resource, go to: [nori-test2](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/9e223dbe-3399-4e19-88eb-0975f02ac87f/resourceGroups/nori-app-insights/providers/microsoft.insights/components/nori-test2/overview)

### Zipkin (Debug Build Only)

For development and testing purposes, telemetry can also be exported to a local Zipkin instance using the `ZipkinExporter`. This is useful for visualizing traces during development.

#### Using Zipkin in the Dev Container

If you're using the provided dev container, Zipkin is automatically started for you! Simply:

1. Open the repository in the dev container (VS Code will prompt you)

2. Build and run the application in DEBUG mode:
   ```bash
   dotnet build -c Debug cirrus.slnx
   ./artifacts/Debug/Core.Cli/az group create --Name mygroup --Location eastus --SubscriptionId <your-subscription-id>
   ```

3. Open Zipkin UI at http://localhost:9411

4. Clicks "Run Query" to see traces

The dev container automatically:
- Starts Zipkin in a separate container
- Configures the `ZIPKIN_ENDPOINT` environment variable
- Forwards port 9411 to your host machine

#### Manual Zipkin Setup

If you're not using the dev container, you can start Zipkin manually:

1. Start Zipkin using Docker:
   ```bash
   docker run -d -p 9411:9411 openzipkin/zipkin
   ```

2. Run the application in DEBUG mode:
   ```bash
   dotnet build -c Debug cirrus.slnx
   ./artifacts/Debug/Core.Cli/az group create --Name mygroup --Location eastus --SubscriptionId <your-subscription-id>
   ```

3. Open Zipkin UI at http://localhost:9411

4. Clicks "Run Query" to see traces

#### Custom Zipkin Endpoint

You can customize the Zipkin endpoint by setting the `ZIPKIN_ENDPOINT` environment variable:

```bash
export ZIPKIN_ENDPOINT=http://my-zipkin-server:9411/api/v2/spans
./artifacts/Debug/Core.Cli/az group create --Name mygroup --Location eastus
```

If not set, the default endpoint is `http://localhost:9411/api/v2/spans`.
