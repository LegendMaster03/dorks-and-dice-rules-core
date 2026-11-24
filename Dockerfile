# Headless runtime only. The Phase 2 branch is not a deployment target.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/RulesCore.Content/RulesCore.Content.csproj src/RulesCore.Content/
COPY src/RulesCore.Mechanics/RulesCore.Mechanics.csproj src/RulesCore.Mechanics/
COPY src/RulesCore.Host/RulesCore.Host.csproj src/RulesCore.Host/
RUN dotnet restore src/RulesCore.Host/RulesCore.Host.csproj

COPY src/RulesCore.Content/ src/RulesCore.Content/
COPY src/RulesCore.Mechanics/ src/RulesCore.Mechanics/
COPY src/RulesCore.Host/ src/RulesCore.Host/
RUN dotnet publish src/RulesCore.Host/RulesCore.Host.csproj \
    --no-restore -c Release -o /app/publish /p:UseAppHost=false

# Isolate authoritative content in the build stage. The final image receives
# only the staged manifests and native trees, not the input source directory.
COPY data/rulesets/ /source/rulesets/
COPY tools/stage_runtime_catalogs.sh /stage_runtime_catalogs.sh
RUN sh /stage_runtime_catalogs.sh /source/rulesets /app/catalogs

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
ENV Rulesets__ContentRoot=/app/data/rulesets
COPY --from=build /app/publish ./
# Runtime image intentionally excludes working/, reference/ and conversions/.
COPY --from=build /app/catalogs/ ./data/rulesets/
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "RulesCore.Host.dll"]
