# E2E Tests

## Setup

```shell
cd setup
docker build -t trustbit/contexture_acceptance_tests_setup .
docker run -e ASPNETCORE_hostBuilder__reloadConfigOnChange=false --rm -p 5000:3000 trustbit/contexture_acceptance_tests_setup
```

## Run

```shell
npm run dev
npm run test:e2e
```
