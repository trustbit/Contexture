FROM mcr.microsoft.com/dotnet/aspnet:5.0
COPY ./ /app
WORKDIR /app
EXPOSE 3000
ENV ASPNETCORE_URLS=http://*:3000
ENV DatabasePath=/data/db.json
ENTRYPOINT ["dotnet", "Contexture.Api.App.dll"]
