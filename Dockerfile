FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /source
COPY . .
RUN dotnet restore src/Sawa3ed.Api/Sawa3ed.Api.csproj
RUN dotnet publish src/Sawa3ed.Api/Sawa3ed.Api.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12 AS final
WORKDIR /app
COPY --from=build /out .
RUN mkdir -p /app/App_Data && chown -R $APP_UID:$APP_UID /app/App_Data
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Sawa3ed.Api.dll"]
