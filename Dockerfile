# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["Vendify.API/Vendify.API.csproj", "Vendify.API/"]
COPY ["Vendify.Application/Vendify.Application.csproj", "Vendify.Application/"]
COPY ["Vendify.Core/Vendify.Core.csproj", "Vendify.Core/"]
COPY ["Vendify.Infrastructure/Vendify.Infrastructure.csproj", "Vendify.Infrastructure/"]
COPY ["Vendify.Shared/Vendify.Shared.csproj", "Vendify.Shared/"]

# Restore packages
RUN dotnet restore "Vendify.API/Vendify.API.csproj"

# Copy all source files
COPY . .

# Build
WORKDIR "/src/Vendify.API"
RUN dotnet build "Vendify.API.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Vendify.API.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# Final stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Vendify.API.dll"]