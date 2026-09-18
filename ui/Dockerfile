FROM node:22-alpine AS build
WORKDIR /ui
COPY ui/package*.json ./
RUN npm ci --no-audit --no-fund
COPY ui/ ./
RUN npm run build

FROM nginx:stable-alpine
COPY ui/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /ui/dist/console/browser /usr/share/nginx/html
EXPOSE 80
