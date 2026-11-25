//! Checked whole-document bindings to a generated LuminPack model library.
pub mod direct;
pub mod primitive;
use libloading::Library;
use std::{
    ffi::{c_char, c_void, CStr},
    path::Path,
    ptr::NonNull,
};

#[derive(Clone, Copy)]
#[repr(i32)]
pub enum Format {
    Binary,
    Utf8,
    Utf16,
}

type Read =
    unsafe extern "C" fn(*const u8, usize, i32, *mut *mut c_void, *mut c_char, usize) -> i32;
type Write =
    unsafe extern "C" fn(*const c_void, *mut u8, usize, i32, *mut usize, *mut c_char, usize) -> i32;
type Free = unsafe extern "C" fn(*mut c_void);

pub struct Codec {
    read: Read,
    write: Write,
    free: Free,
    _library: Library,
}

pub struct Document<'a> {
    codec: &'a Codec,
    value: NonNull<c_void>,
}

impl Codec {
    /// Constructs a native document from a value with the model's JSON representation.
    #[cfg(feature = "values")]
    pub fn from_value<T: serde::Serialize>(&self, value: &T) -> Result<Document<'_>, String> {
        let input = serde_json::to_vec(value).map_err(|e| e.to_string())?;
        self.decode(&input, Format::Utf8)
    }

    /// # Safety
    /// The path and its dependencies must be trusted libraries implementing the LuminPack model ABI.
    pub unsafe fn load(path: impl AsRef<Path>) -> Result<Self, String> {
        let lib = Library::new(path.as_ref()).map_err(|e| e.to_string())?;
        let abi = lib
            .get::<unsafe extern "C" fn() -> u32>(b"lp_abi")
            .map_err(|e| e.to_string())?;
        if abi() != 1 {
            return Err("Unsupported LuminPack model ABI".into());
        }
        Ok(Self {
            read: *lib.get(b"lp_read").map_err(|e| e.to_string())?,
            write: *lib.get(b"lp_write").map_err(|e| e.to_string())?,
            free: *lib.get(b"lp_free").map_err(|e| e.to_string())?,
            _library: lib,
        })
    }

    pub fn decode(&self, input: &[u8], format: Format) -> Result<Document<'_>, String> {
        let mut err = [0; 256];
        let mut value = std::ptr::null_mut();
        unsafe {
            if (self.read)(
                input.as_ptr(),
                input.len(),
                format as i32,
                &mut value,
                err.as_mut_ptr(),
                err.len(),
            ) != 0
            {
                return Err(CStr::from_ptr(err.as_ptr()).to_string_lossy().into_owned());
            }
        }
        Ok(Document {
            codec: self,
            value: NonNull::new(value).ok_or("Missing document")?,
        })
    }
}

impl Document<'_> {
    /// Materializes an application value using caller-owned UTF-8 scratch space.
    #[cfg(feature = "values")]
    pub fn to_value<T: serde::de::DeserializeOwned>(
        &self,
        scratch: &mut [u8],
    ) -> Result<T, String> {
        serde_json::from_slice(self.encode_into(scratch, Format::Utf8)?).map_err(|e| e.to_string())
    }

    /// Returns the written prefix. On error the destination may be partially written.
    pub fn encode_into<'b>(
        &self,
        output: &'b mut [u8],
        format: Format,
    ) -> Result<&'b [u8], String> {
        let mut err = [0; 256];
        let mut written = 0;
        unsafe {
            if (self.codec.write)(
                self.value.as_ptr(),
                output.as_mut_ptr(),
                output.len(),
                format as i32,
                &mut written,
                err.as_mut_ptr(),
                err.len(),
            ) != 0
            {
                return Err(CStr::from_ptr(err.as_ptr()).to_string_lossy().into_owned());
            }
        }
        output
            .get(..written)
            .ok_or_else(|| "Invalid native result length".into())
    }
}

impl Drop for Document<'_> {
    fn drop(&mut self) {
        unsafe { (self.codec.free)(self.value.as_ptr()) }
    }
}
