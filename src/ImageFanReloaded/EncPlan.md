# 🔐 EncFile Photo Viewer – Design Plan

## 🎯 Goal

Build a cross-platform photo viewer for `.enc` files that:

- Decrypts images **in memory only**
- Leaves **no traces on disk**
- Provides smooth navigation and good UX
- Scales efficiently (no excessive memory usage)

---

## 🧱 Core Principles

1. **Security First**
   - No decrypted files written to disk (unless explicitly requested)
   - No long-lived secrets in memory

2. **Performance**
   - On-demand decryption
   - Minimal memory footprint
   - Smart caching

3. **Simplicity**
   - Focus only on required features (image viewing)
   - Avoid unnecessary complexity from full-featured viewers

---

## 🔐 Password & Key Handling

### Flow

```

User enters password
↓
Derive key (PBKDF2 / Argon2)
↓
Clear raw password immediately
↓
Store derived key in memory

````

### Rules

- ❌ Do NOT store password string
- ✅ Store only derived key (`byte[]`)
- ✅ Zero out key on:
  - App exit
  - Lock action
  - Manual clear

---

## 📂 File Discovery & Filtering

### Process

1. Scan directory for `.enc` files
2. Use:

```csharp
EncFile.PeekMetadata(file)
````

3. Extract:

```csharp
mimeType = meta["mimeType"]
```

### Filtering

* ✅ Show only `image/*` for viewer
* 🚫 Skip non-image files (or handle later)

---

## 🖼️ Image Viewing Pipeline

### Flow

```
.enc file
   ↓
PeekMetadata()
   ↓
User selects image
   ↓
Decrypt to stream
   ↓
Load into Bitmap
   ↓
Display
```

### Implementation (concept)

```csharp
using var stream = DecryptToStream(filePath);
var bitmap = new Bitmap(stream);
imageControl.Source = bitmap;
```

---

## ⚡ Decryption Strategy

### ✅ Recommended

* Decrypt **on demand**
* Use **stream-based decryption**

### ❌ Avoid

* Decrypting all images upfront
* Loading entire file into memory (`byte[]`)

---

## 🧠 Memory Management

### Strategy: Small Cache

Keep only:

```
[Previous] [Current] [Next]
```

Optional:

* Preload next image in background

### Eviction

* Dispose old images:

```csharp
bitmap.Dispose();
```

### Why?

* Prevents high RAM usage
* Keeps app responsive

---

## 🧹 Memory Controls (UI)

### 1. Clear Cache

* Dispose all bitmaps
* Free memory
* Keep session active

### 2. Lock Viewer

* Clear cache
* Zero out key
* Require password again

---

## 💾 Decrypt to Disk Feature

### Behavior

* User selects `.enc` file
* Clicks **"Decrypt & Save"**

### Safeguards

* Show warning:

  > "This will create an unencrypted copy on disk"

### Optional Enhancements

* Save as:

  ```
  filename.decrypted.jpg
  ```
* Auto-delete option (future)

---

## 🚀 Performance Enhancements

### 1. Preloading

* Load next image in background

### 2. Lazy Loading

* Only decrypt when needed

### 3. Stream Processing

* Avoid large memory allocations

---

## 🖥️ UI Features (MVP)

* Image display
* Next / Previous navigation
* Keyboard support:

  * `←` Previous
  * `→` Next
* Zoom + pan
* File list / gallery view

---

## 📸 Future Enhancements

### 1. Thumbnail Support

* Store thumbnail in metadata
* Faster browsing without full decrypt

### 2. Video Support

* Extend MIME handling (`video/*`)

### 3. Session Timeout

* Auto-lock after inactivity

### 4. Cross-platform Apps

* Desktop: Avalonia
* Web: Blazor (in-memory decrypt)
* Mobile: MAUI / Flutter

---

## 🧩 Architecture Overview

```
EncFile (.enc)
   ↓
PeekMetadata()
   ↓
MIME Filter (image/*)
   ↓
Decrypt Stream (using key)
   ↓
Bitmap Loader
   ↓
UI Viewer (Avalonia)
   ↓
Cache Layer (limited)
```

---

## ⚠️ Security Notes

* Never write decrypted data to disk automatically
* Avoid long-lived secrets in memory
* Clear sensitive data explicitly
* Prefer streams over buffers

---

## ✅ Summary

### Keep

* Metadata-based MIME detection
* In-memory decryption
* Stream-based loading

### Avoid

* Storing password long-term
* Keeping all images in memory
* Writing temp files

### Add

* Small cache (3–10 images)
* Key derivation + wiping
* Clear cache / lock controls

---

## 🎯 Outcome

A fast, secure, and lightweight encrypted image viewer that:

* Protects user data
* Performs efficiently
* Works cross-platform

```

---

