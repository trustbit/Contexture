# E2E Tests

## Setup

```shell
cd setup
docker run -d --rm -p 3000:3000 -e ASPNETCORE_hostBuilder__reloadConfigOnChange=false -it $(docker build -q .)
```

## Run

```shell
npm run test:e2e
```
