# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["Amethral.Api/Amethral.Api.csproj", "Amethral.Api/"]
RUN dotnet restore "Amethral.Api/Amethral.Api.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/Amethral.Api"
RUN dotnet build "Amethral.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Amethral.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Install EF Core tools for migrations
RUN dotnet tool install --global dotnet-ef --version 10.0.0
ENV PATH="${PATH}:/root/.dotnet/tools"

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Install dotnet-ef tool in runtime image for migrations
COPY --from=mcr.microsoft.com/dotnet/sdk:10.0 /usr/share/dotnet /usr/share/dotnet
RUN ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet
RUN dotnet tool install --global dotnet-ef --version 10.0.0
ENV PATH="${PATH}:/root/.dotnet/tools"

# Copy published app
COPY --from=publish /app/publish .

# Copy source files needed for migrations
COPY --from=build /src/Amethral.Api /app/src

# Copy entrypoint script
COPY entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh

EXPOSE 8080

ENTRYPOINT ["/app/entrypoint.sh"]
