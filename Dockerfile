# Headless runtime only. This image is not deployed by the rewrite branch.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/RulesCore.Content/RulesCore.Content.csproj src/RulesCore.Content/
COPY src/RulesCore.Host/RulesCore.Host.csproj src/RulesCore.Host/
RUN dotnet restore src/RulesCore.Host/RulesCore.Host.csproj

COPY src/RulesCore.Content/ src/RulesCore.Content/
COPY src/RulesCore.Host/ src/RulesCore.Host/
RUN dotnet publish src/RulesCore.Host/RulesCore.Host.csproj \
    --no-restore -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "RulesCore.Host.dll"]
