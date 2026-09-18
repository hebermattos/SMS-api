FROM mcr.microsoft.com/dotnet/sdk:8.0
WORKDIR /src
COPY . .
RUN dotnet restore Sms.Api.sln
RUN dotnet build Sms.Api.sln --configuration Release --no-restore
ENTRYPOINT ["dotnet", "test", "Sms.Api.sln", "--configuration", "Release", "--no-build", "--filter", "FullyQualifiedName~BandwidthWebhookSqlTests|FullyQualifiedName~AdministrationSqlTests|FullyQualifiedName~ExampleProviderSeedSqlTests|FullyQualifiedName~AdministratorSqlTests"]
