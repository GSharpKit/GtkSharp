#!/usr/bin/bash
rm -rf BuildOutput/*
find . -type d -iname "Generated" -exec rm -rf {} \;
find . -type d -iname "obj" -exec rm -rf {} \;
