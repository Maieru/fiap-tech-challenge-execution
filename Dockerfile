FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN mkdir -p artifacts/packages
RUN dotnet nuget add source /src/artifacts/packages --name local-review --configfile NuGet.Config
RUN dotnet publish src/FIAP.TechChallenge.Execution.API -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "FIAP.TechChallenge.Execution.API.dll"]
