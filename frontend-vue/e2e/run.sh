# bash
cd setup && docker run -d --rm -p 3000:3000 -e ASPNETCORE_hostBuilder__reloadConfigOnChange=false -it $(docker build -q .)
npm run test:e2e