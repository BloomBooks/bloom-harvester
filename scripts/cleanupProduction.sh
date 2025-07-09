#!/bin/sh

df

cd /c/Users/LSProduction/AppData/Local/BloomHarvester

curl -X GET \
  -H "X-Parse-Application-Id: ${BloomHarvesterParseAppIdProd}" -G \
  --data-urlencode "keys=objectId" \
  "https://bloom-parse-server-production.azurewebsites.net/parse/classes/books?limit=99999" \
  | sed 's/{"objectId":/\n{"objectId":/g' > allIds.json

cd Prod && for f in *; do if [ ! `fgrep "$f" ../allIds.json` ]; then echo deleting $f; rm -rf "$f"; fi; done

df
