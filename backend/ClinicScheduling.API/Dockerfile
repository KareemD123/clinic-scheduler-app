FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["ClinicScheduling.API/ClinicScheduling.API.csproj", "ClinicScheduling.API/"]
COPY ["ClinicScheduling.Application/ClinicScheduling.Application.csproj", "ClinicScheduling.Application/"]
COPY ["ClinicScheduling.Domain/ClinicScheduling.Domain.csproj", "ClinicScheduling.Domain/"]
COPY ["ClinicScheduling.Infrastructure/ClinicScheduling.Infrastructure.csproj", "ClinicScheduling.Infrastructure/"]

RUN dotnet restore "ClinicScheduling.API/ClinicScheduling.API.csproj"

COPY . .
WORKDIR "/src/ClinicScheduling.API"
RUN dotnet build "ClinicScheduling.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ClinicScheduling.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY ClinicScheduling.API/database.json .

# Set environment variables for Azure
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "ClinicScheduling.API.dll"]
