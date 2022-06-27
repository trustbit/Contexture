FROM mcr.microsoft.com/dotnet/aspnet:6.0
COPY ./ /app
WORKDIR /app
EXPOSE 3000
ARG GIT_HASH=unspecified
LABEL org.opencontainers.image.revision=$GIT_HASH
ENV ASPNETCORE_URLS=http://*:3000
ENV DatabasePath=/data/db.json
ENV GitHash=$GIT_HASH
ENTRYPOINT ["dotnet", "Contexture.Api.App.dll"]
