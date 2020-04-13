
# covid19cases

Explore NY Times #COVID19 cases data

  

## Requirements
Visual Studio Code console project
https://code.visualstudio.com/

.NET Core 3.1
https://dotnet.microsoft.com/download/dotnet-core/3.1

## appsettings.json
The "stateFilter" array allows restricting state and county data to the states contained in the array. 

If the array is empty, then **all** states and counties are listed.

**Example**
Restrict output to just the states of Kansas and Missouri
```
"stateFilter": [
    "Kansas",
    "Missouri"
]
