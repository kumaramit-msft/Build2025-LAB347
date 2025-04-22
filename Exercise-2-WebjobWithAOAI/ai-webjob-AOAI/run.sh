#!/bin/bash

echo "Starting AI webjob"
start_time=$(date +%s)
dotnet ai-webjob.dll
end_time=$(date +%s)
elapsed_time=$((end_time - start_time))
echo "Time taken: ${elapsed_time} seconds"
echo "AI webjob finshed"
