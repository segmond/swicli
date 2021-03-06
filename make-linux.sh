#!/bin/bash

mkdir -p swicli-inst/pl/lib/i386-linux
mkdir -p swicli-inst/pl/lib/x86_64-linux
mkdir -p swicli-inst/pl/library/
swipl-ld -m64 -shared -o swicli-inst/pl/lib/x86_64-linux/swicli.so swicli.c `pkg-config --cflags --libs mono-2` -lm
swipl-ld -m32 -shared -o swicli-inst/pl/lib/i386-linux/swicli32.so swicli32.c `pkg-config --cflags --libs mono-2` -lm

cp swicli.pl swicli-inst/pl/library/
mcs -unsafe -warn:0 Swicli.Library/*.cs -out:Swicli.Library.dll
cp Swicli.Library.dll swicli-inst/pl/lib/x86_64-linux/
mv  Swicli.Library.dll swicli-inst/pl/lib/i386-linux/

cp d*.html swicli-inst/
cp RE*.* swicli-inst/
cp in*.* swicli-inst/

