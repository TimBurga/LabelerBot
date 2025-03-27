FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER app
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["./LabelerBot.Service/LabelerBot.Service.csproj", "LabelerBot.Service/"]
COPY ["./LabelerBot.Data/LabelerBot.Data.csproj", "LabelerBot.Data/"]
RUN dotnet restore "./LabelerBot.Service.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./LabelerBot.Service.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./LabelerBot.Service.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LabelerBot.Service.dll"]