# bash
cd setup && docker run -d --rm -p 3000:3000 -e ASPNETCORE_hostBuilder__reloadConfigOnChange=false -it $(docker build -q .)
printf '\nWaiting for server to accept requests...\n'
until curl --output /dev/null --silent --fail http://localhost:3000/api/domains
; do
    printf '.'
    sleep 1
done
printf '\nServer is accepting requests. Starting tests.\n'
npm run test:e2e