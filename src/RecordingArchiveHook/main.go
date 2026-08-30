package main

import (
    "fmt"
    "os"
    "path/filepath"
    "strings"
)

func main() {
    segment := strings.TrimSpace(os.Getenv("MTX_SEGMENT_PATH"))
    if segment == "" {
        fmt.Fprintln(os.Stderr, "MTX_SEGMENT_PATH is required")
        os.Exit(2)
    }

    clean := filepath.Clean(segment)
    if filepath.Ext(clean) != ".mp4" {
        fmt.Fprintln(os.Stderr, "recording segment must be an MP4 file")
        os.Exit(2)
    }

    marker := clean + ".ready"
    file, err := os.OpenFile(marker, os.O_CREATE|os.O_WRONLY|os.O_TRUNC, 0o600)
    if err != nil {
        fmt.Fprintln(os.Stderr, "unable to create finalized marker")
        os.Exit(1)
    }

    if err := file.Close(); err != nil {
        fmt.Fprintln(os.Stderr, "unable to close finalized marker")
        os.Exit(1)
    }
}
