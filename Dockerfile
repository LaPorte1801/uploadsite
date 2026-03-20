FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY UploadSite.sln ./
COPY UploadSite.Web/UploadSite.Web.csproj UploadSite.Web/
RUN dotnet restore UploadSite.Web/UploadSite.Web.csproj

COPY UploadSite.Web/. UploadSite.Web/
RUN dotnet publish UploadSite.Web/UploadSite.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV UPLOADSITE_DB_PATH=/data/uploadsite.db
ENV UPLOADSITE_STAGING_ROOT=/data/staging
ENV UPLOADSITE_LIBRARY_ROOT=/music

COPY --from=build /app/publish ./

VOLUME ["/data", "/music"]
EXPOSE 8080

ENTRYPOINT ["dotnet", "UploadSite.Web.dll"]
