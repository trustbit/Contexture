.PHONY: publish-backend build-image

build-backend:
	cd backend && dotnet build

test-backend:
	cd backend && dotnet test

publish-backend:
	dotnet publish backend/Contexture.Api/Contexture.Api.fsproj \
        -c Release \
        -o artifacts/backend

build-app:
	cd frontend && elm make src/Main.elm

publish-app:
	cd frontend && elm make src/Main.elm --output=../artifacts/frontend/index.html

prepare-image: publish-backend publish-app
	mkdir -p artifacts/image/
	cp -r artifacts/backend/ artifacts/image/
	cp artifacts/frontend/*.* artifacts/image/wwwroot/

build-image: prepare-image
	cd artifacts/image && docker build -t softwarepark/contexture -f Dockerfile .

run-image: build-image
	docker run -it softwarepark/contexture

run-app:
	docker run -p 3000:3000 contexture-dotnet