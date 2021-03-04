FROM mcr.microsoft.com/dotnet/aspnet:5.0
COPY bin/Release/net5.0/publish/ /app
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://*:3000
ENTRYPOINT ["dotnet", "Contexture.Api.App.dll"]
