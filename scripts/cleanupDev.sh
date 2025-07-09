#!/bin/sh

df

cd /c/Users/LSDeveloper/AppData/Local/BloomHarvester

curl -X GET \
  -H "X-Parse-Application-Id: ${BloomHarvesterParseAppIdDev}" -G \
  --data-urlencode "keys=objectId" \
  "https://bloom-parse-server-develop.azurewebsites.net/parse/classes/books?limit=20000" \
  | sed 's/{"objectId":/\n{"objectId":/g' > allIds.json

cd Dev && for f in *; do if [ ! `fgrep "$f" ../allIds.json` ]; then echo deleting $f; rm -rf "$f"; fi; done

df
