# Use standard images for better compatibility
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for better layer caching
COPY ["ClinicScheduling.API/ClinicScheduling.API.csproj", "ClinicScheduling.API/"]
COPY ["ClinicScheduling.Application/ClinicScheduling.Application.csproj", "ClinicScheduling.Application/"]
COPY ["ClinicScheduling.Domain/ClinicScheduling.Domain.csproj", "ClinicScheduling.Domain/"]
COPY ["ClinicScheduling.Infrastructure/ClinicScheduling.Infrastructure.csproj", "ClinicScheduling.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "ClinicScheduling.API/ClinicScheduling.API.csproj"

# Copy source code
COPY . .
WORKDIR "/src/ClinicScheduling.API"

# Build the project
RUN dotnet build "ClinicScheduling.API.csproj" -c Release -o /app/build

# Publish without specifying runtime (let it use the default for Alpine)
FROM build AS publish
RUN dotnet publish "ClinicScheduling.API.csproj" -c Release -o /app/publish --no-restore

# Final stage - minimal runtime
FROM base AS final
WORKDIR /app

# Copy published app
COPY --from=publish /app/publish .
COPY ClinicScheduling.API/database.json .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT ["dotnet", "ClinicScheduling.API.dll"]
