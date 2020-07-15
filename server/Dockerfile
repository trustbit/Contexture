FROM node:12

WORKDIR /usr/src/app

COPY package*.json ./
RUN npm ci --only=production
COPY . .

EXPOSE 4000

VOLUME /usr/src/app/data

CMD [ "node", "server.js" ]