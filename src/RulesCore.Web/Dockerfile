FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/RulesCore.Domain/RulesCore.Domain.csproj", "src/RulesCore.Domain/"]
COPY ["src/RulesCore.Application/RulesCore.Application.csproj", "src/RulesCore.Application/"]
COPY ["src/RulesCore.Infrastructure/RulesCore.Infrastructure.csproj", "src/RulesCore.Infrastructure/"]
COPY ["src/RulesCore.Web/RulesCore.Web.csproj", "src/RulesCore.Web/"]
RUN dotnet restore "src/RulesCore.Web/RulesCore.Web.csproj"

COPY . .
RUN dotnet publish "src/RulesCore.Web/RulesCore.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RulesCore.Web.dll"]
