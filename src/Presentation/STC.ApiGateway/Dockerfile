FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Presentation/STC.ApiGateway/STC.ApiGateway.csproj", "src/Presentation/STC.ApiGateway/"]
COPY ["STC.Shared/src/Utilities/STC.Shared.JwtAuthentication/STC.Shared.JwtAuthentication.csproj", "STC.Shared/src/Utilities/STC.Shared.JwtAuthentication/"]
COPY ["STC.Shared/src/Contracts/STC.Shared.Contracts.Auth/STC.Shared.Contracts.Auth.csproj", "STC.Shared/src/Contracts/STC.Shared.Contracts.Auth/"]
RUN dotnet restore "src/Presentation/STC.ApiGateway/STC.ApiGateway.csproj"
COPY . .
WORKDIR "/src/src/Presentation/STC.ApiGateway"
RUN dotnet build "STC.ApiGateway.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "STC.ApiGateway.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "STC.ApiGateway.dll"]
