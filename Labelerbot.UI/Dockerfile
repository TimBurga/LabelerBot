FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["LabelerBot.UI/LabelerBot.UI.csproj", "LabelerBot.UI/"]
COPY ["LabelerBot.Data/LabelerBot.Data.csproj", "LabelerBot.Data/"]
RUN dotnet restore "./LabelerBot.UI/LabelerBot.UI.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./LabelerBot.UI.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./LabelerBot.UI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LabelerBot.UI.dll"]