FROM mcr.microsoft.com/dotnet/sdk:8.0
WORKDIR /src
COPY . .
RUN dotnet restore TextRelay.Api.sln
RUN dotnet build TextRelay.Api.sln --configuration Release --no-restore
ENTRYPOINT ["dotnet", "test", "TextRelay.Api.sln", "--configuration", "Release", "--no-build", "--filter", "Category=Integration"]
