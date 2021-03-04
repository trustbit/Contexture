.PHONY: publish-server build-image

build-server:
	dotnet build

test-server:
	dotnet test

publish-server:
	dotnet publish Contexture.Api/Contexture.Api.fsproj \
        -c Release \
        -o artifacts/server

build-app:
	cd app && elm make src/Main.elm

publish-app:
	cd app && elm make src/Main.elm --output=../artifacts/app/index.html

prepare-image: publish-server publish-app
	mkdir -p artifacts/image/
	cp -r artifacts/server/ artifacts/image/
	cp artifacts/app/*.* artifacts/image/wwwroot/

build-image: prepare-image
	cd artifacts/image && docker build -t softwarepark/contexture -f Dockerfile .

run-app:
	docker run -p 3000:3000 contexture-dotnet