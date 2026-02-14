$Omni = "http://localhost:5000"

function oa-note-new($title, $body) {
    Invoke-RestMethod -Method POST `
        -Uri "$Omni/api/Notes" `
        -ContentType "application/json" `
        -Body (@{ title=$title; bodyMarkdown=$body } | ConvertTo-Json)
}

function oa-notes() {
    Invoke-RestMethod -Method GET `
        -Uri "$Omni/api/Notes"
}

function oa-tag-new($name, $category="") {
    $payload = @{ name=$name }
    if ($category -ne "") { $payload.category = $category }

    Invoke-RestMethod -Method POST `
        -Uri "$Omni/api/Tags" `
        -ContentType "application/json" `
        -Body ($payload | ConvertTo-Json)
}

function oa-tags() {
    Invoke-RestMethod -Method GET `
        -Uri "$Omni/api/Tags"
}

function oa-tag-link($tagId, $noteId) {
    Invoke-RestMethod -Method POST `
        -Uri "$Omni/api/Tags/$tagId/notes/$noteId"
}

function oa-notes-bytag([string[]]$tags) {
    $qs = ($tags | ForEach-Object { "tag=$_" }) -join "&"
    Invoke-RestMethod -Method GET `
        -Uri "$Omni/api/Notes?$qs"
}

function oa-search($q) {
    Invoke-RestMethod -Method GET `
        -Uri "$Omni/api/Notes/search?q=$([uri]::EscapeDataString($q))"
}
