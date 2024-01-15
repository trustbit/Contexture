.PHONY: build-image

build-backend:
	cd backend && dotnet build

test-backend:
	cd backend && dotnet test \
	    --logger "trx;LogFileName=TestResults.trx"

publish-backend:
	dotnet publish backend/Contexture.Api/Contexture.Api.fsproj \
        -c Release \
        -o artifacts/backend

publish-docker:
	dotnet publish backend/Contexture.Api/Contexture.Api.fsproj \
        -c Release \
        --os linux \
        -o artifacts/backend

build-app:
	cd frontend-vue && npm ci && npm run build

publish-app: build-app
	mkdir -p artifacts/frontend
	cp -r frontend-vue/dist/** artifacts/frontend

prepare-image: publish-docker publish-app
	mkdir -p artifacts/image/wwwroot
	cp -r artifacts/backend/*.* artifacts/image/
	cp entrypoint.sh artifacts/image/entrypoint.sh
	cp -r artifacts/frontend/** artifacts/image/wwwroot/

build-image: prepare-image
	cd artifacts/image && docker build -t softwarepark/contexture -f ../backend/Dockerfile .

run-app:
	docker run -it -p 3000:3000 -v contexture_data:/data softwarepark/contexture

run-image: build-image run-app

clean:
	rm -rf artifacts/
