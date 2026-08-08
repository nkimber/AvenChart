FROM node:24-alpine AS build
WORKDIR /app

COPY avenchart-ui/package*.json ./
RUN npm ci

COPY avenchart-ui/ ./
ENV VITE_API_BASE_URL=
RUN npm run build

FROM nginx:1.29-alpine

ARG AVENCHART_BASE_URL=""

COPY infra/azure/demo/avenchart-ui-nginx.conf.template /tmp/default.conf.template
RUN if [ -z "$AVENCHART_BASE_URL" ]; then \
      echo "AVENCHART_BASE_URL build argument is required." >&2; \
      exit 1; \
    fi; \
    clean_url="${AVENCHART_BASE_URL%/}"; \
    sed "s|__AVENCHART_BASE_URL__|${clean_url}|g" /tmp/default.conf.template > /etc/nginx/conf.d/default.conf

COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
