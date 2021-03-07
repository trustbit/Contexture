FROM mcr.microsoft.com/dotnet/aspnet:5.0
COPY ./ /app
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://*:3000
ENTRYPOINT ["dotnet", "Contexture.Api.App.dll"]
