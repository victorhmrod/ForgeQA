FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ForgeQA.slnx ./
COPY src/ForgeQA.Domain/ForgeQA.Domain.csproj src/ForgeQA.Domain/
COPY src/ForgeQA.Application/ForgeQA.Application.csproj src/ForgeQA.Application/
COPY src/ForgeQA.Infrastructure/ForgeQA.Infrastructure.csproj src/ForgeQA.Infrastructure/
COPY src/ForgeQA.Api/ForgeQA.Api.csproj src/ForgeQA.Api/
COPY tests/ForgeQA.UnitTests/ForgeQA.UnitTests.csproj tests/ForgeQA.UnitTests/
COPY tests/ForgeQA.IntegrationTests/ForgeQA.IntegrationTests.csproj tests/ForgeQA.IntegrationTests/
RUN dotnet restore src/ForgeQA.Api/ForgeQA.Api.csproj

COPY . .
RUN dotnet publish src/ForgeQA.Api/ForgeQA.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ForgeQA.Api.dll"]
