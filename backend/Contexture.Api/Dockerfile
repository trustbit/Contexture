FROM mcr.microsoft.com/dotnet/aspnet:5.0
COPY ./ /app
WORKDIR /app
EXPOSE 4000
ENV ASPNETCORE_URLS=http://*:4000
ENV DATABASEPATH=/data/db.json
ENTRYPOINT ["dotnet", "Contexture.Api.App.dll"]
