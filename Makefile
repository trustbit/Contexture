.PHONY: build-image

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
	mkdir -p artifacts/frontend
	cd frontend && elm make src/Main.elm --output=../artifacts/frontend/index.html

prepare-image: publish-backend publish-app
	mkdir -p artifacts/image/wwwroot
	cp -r artifacts/backend/ artifacts/image/
	cp -r artifacts/frontend/*.* artifacts/image/wwwroot/

build-image: prepare-image
	cd artifacts/image && docker build -t softwarepark/contexture -f Dockerfile .

run-app:
	docker run -it -p 4000:4000 -v contexture_data:/data softwarepark/contexture

run-image: build-image run-app
