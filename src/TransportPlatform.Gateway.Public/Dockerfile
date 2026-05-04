FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/TransportPlatform.Gateway.Public/TransportPlatform.Gateway.Public.csproj", "src/TransportPlatform.Gateway.Public/"]
RUN dotnet restore "src/TransportPlatform.Gateway.Public/TransportPlatform.Gateway.Public.csproj"
COPY . .
WORKDIR "/src/src/TransportPlatform.Gateway.Public"
RUN dotnet build "TransportPlatform.Gateway.Public.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TransportPlatform.Gateway.Public.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TransportPlatform.Gateway.Public.dll"]
