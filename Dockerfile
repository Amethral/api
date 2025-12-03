# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy everything first (including submodules)
COPY . .

# Restore dependencies
RUN dotnet restore "Amethral.Api/Amethral.Api.csproj"

# Build
WORKDIR "/src/Amethral.Api"
RUN dotnet build "Amethral.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Amethral.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Install EF Core tools for migrations
RUN dotnet tool install --global dotnet-ef --version 10.0.0
ENV PATH="${PATH}:/root/.dotnet/tools"

# Create EF Migrations Bundle
RUN dotnet ef migrations bundle --project Amethral.Api.csproj -o /app/publish/efbundle

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Copy published app (includes efbundle)
COPY --from=publish /app/publish .

# Copy entrypoint script
COPY entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh

EXPOSE 80

ENTRYPOINT ["/app/entrypoint.sh"]
