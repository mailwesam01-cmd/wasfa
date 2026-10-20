# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY Wasfa.slnx .
COPY src/Wasfa.Domain/Wasfa.Domain.csproj src/Wasfa.Domain/
COPY src/Wasfa.Safety/Wasfa.Safety.csproj src/Wasfa.Safety/
COPY src/Wasfa.Infrastructure/Wasfa.Infrastructure.csproj src/Wasfa.Infrastructure/
COPY src/Wasfa.Api/Wasfa.Api.csproj src/Wasfa.Api/
COPY tests/Wasfa.Safety.Tests/Wasfa.Safety.Tests.csproj tests/Wasfa.Safety.Tests/
COPY tests/Wasfa.Api.IntegrationTests/Wasfa.Api.IntegrationTests.csproj tests/Wasfa.Api.IntegrationTests/

RUN dotnet restore src/Wasfa.Api/Wasfa.Api.csproj

COPY src/ src/
COPY knowledge-base/ /app/knowledge-base/
WORKDIR /source/src/Wasfa.Api
RUN dotnet publish -c Release -o /app/publish

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENV KnowledgeBase__Path=/app/knowledge-base

COPY --from=build /app/publish .
COPY knowledge-base/ /app/knowledge-base/

ENTRYPOINT ["dotnet", "Wasfa.Api.dll"]
