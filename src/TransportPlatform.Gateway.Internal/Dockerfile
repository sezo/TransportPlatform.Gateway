FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/TransportPlatform.Gateway.Internal/TransportPlatform.Gateway.Internal.csproj", "src/TransportPlatform.Gateway.Internal/"]
RUN dotnet restore "src/TransportPlatform.Gateway.Internal/TransportPlatform.Gateway.Internal.csproj"
COPY . .
WORKDIR "/src/src/TransportPlatform.Gateway.Internal"
RUN dotnet build "TransportPlatform.Gateway.Internal.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TransportPlatform.Gateway.Internal.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TransportPlatform.Gateway.Internal.dll"]