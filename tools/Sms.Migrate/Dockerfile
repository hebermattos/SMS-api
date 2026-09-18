FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore tools/Sms.Migrate/Sms.Migrate.csproj
RUN dotnet publish tools/Sms.Migrate/Sms.Migrate.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
COPY database /app/database
ENTRYPOINT ["dotnet","Sms.Migrate.dll"]
