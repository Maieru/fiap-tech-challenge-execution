FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/ src/
RUN dotnet publish src/FIAP.TechChallenge.Execution.API/FIAP.TechChallenge.Execution.API.csproj -c Release -o /app -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "FIAP.TechChallenge.Execution.API.dll"]
