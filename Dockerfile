# Optional: deploy with `heroku container:push web` or any container host (Fly, Railway, etc.)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY PaApp.sln ./
COPY PaApp/PaApp.csproj PaApp/
RUN dotnet restore PaApp/PaApp.csproj
COPY PaApp/ PaApp/
RUN dotnet publish PaApp/PaApp.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# PORT is set at runtime (Heroku, Cloud Run, etc.); Program.cs also reads PORT.
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PaApp.dll"]
