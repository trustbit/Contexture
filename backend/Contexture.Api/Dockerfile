FROM mcr.microsoft.com/dotnet/aspnet:7.0
COPY ./ /app
WORKDIR /app
EXPOSE 3000
ARG GIT_HASH=unspecified
LABEL org.opencontainers.image.revision=$GIT_HASH
ENV ASPNETCORE_URLS=http://*:3000
ENV FileBased__Path=/data/db.json
ENV GitHash=$GIT_HASH
RUN ["chmod", "+x", "./entrypoint.sh"]
ENTRYPOINT ["./entrypoint.sh"]
