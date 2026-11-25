//! Checked little-endian primitives used by generated positional codecs.
pub struct Reader<'a> {
    remaining: &'a [u8],
    collection_limit: usize,
    string_limit: usize,
}

pub type Result<T> = std::result::Result<T, &'static str>;

impl<'a> Reader<'a> {
    pub fn new(data: &'a [u8], collection_limit: usize, string_limit: usize) -> Self {
        Self {
            remaining: data,
            collection_limit,
            string_limit,
        }
    }
    pub fn take(&mut self, size: usize) -> Result<&'a [u8]> {
        let (head, tail) = self
            .remaining
            .split_at_checked(size)
            .ok_or("Truncated binary input")?;
        self.remaining = tail;
        Ok(head)
    }
    pub fn complete(&self) -> bool {
        self.remaining.is_empty()
    }
    pub fn byte(&mut self) -> Result<u8> {
        Ok(self.take(1)?[0])
    }
    pub fn boolean(&mut self) -> Result<bool> {
        match self.byte()? {
            0 => Ok(false),
            1 => Ok(true),
            _ => Err("Invalid boolean"),
        }
    }
    pub fn int(&mut self) -> Result<i32> {
        Ok(i32::from_le_bytes(self.take(4)?.try_into().unwrap()))
    }
    pub fn long(&mut self) -> Result<i64> {
        Ok(i64::from_le_bytes(self.take(8)?.try_into().unwrap()))
    }
    pub fn float(&mut self) -> Result<f32> {
        Ok(f32::from_le_bytes(self.take(4)?.try_into().unwrap()))
    }
    pub fn count(&mut self, minimum: usize) -> Result<Option<usize>> {
        let n = self.int()?;
        if n == -1 {
            return Ok(None);
        }
        let size = usize::try_from(n).map_err(|_| "Negative collection length")?;
        if size > self.collection_limit
            || size
                .checked_mul(minimum)
                .is_none_or(|n| n > self.remaining.len())
        {
            return Err("Invalid collection length");
        }
        Ok(Some(size))
    }
    pub fn text(&mut self) -> Result<Option<String>> {
        let header = self.int()?;
        if header == -1 {
            return Ok(None);
        }
        let size = usize::try_from(header).map_err(|_| "Invalid string length")?;
        if size / 2 > self.string_limit {
            return Err("String exceeds configured limit");
        }
        if size & 1 != 0 {
            let bytes = self.take(size / 2)?;
            if bytes.is_ascii() {
                // ASCII validation above proves every copied byte is valid UTF-8.
                return Ok(Some(unsafe { String::from_utf8_unchecked(bytes.to_vec()) }));
            }
            return Ok(Some(bytes.iter().map(|&v| char::from(v)).collect()));
        }
        let units = self
            .take(size)?
            .chunks_exact(2)
            .map(|p| u16::from_le_bytes([p[0], p[1]]));
        char::decode_utf16(units)
            .collect::<std::result::Result<String, _>>()
            .map(Some)
            .map_err(|_| "Isolated surrogate cannot be represented by String")
    }
}

pub fn write_count(out: &mut Vec<u8>, n: usize) -> Result<()> {
    out.extend_from_slice(
        &i32::try_from(n)
            .map_err(|_| "Collection too large")?
            .to_le_bytes(),
    );
    Ok(())
}

pub fn write_text(out: &mut Vec<u8>, value: Option<&str>) -> Result<()> {
    let Some(s) = value else {
        out.extend_from_slice(&(-1i32).to_le_bytes());
        return Ok(());
    };
    if s.is_ascii() {
        let n = i32::try_from(s.len())
            .ok()
            .and_then(|n| n.checked_mul(2))
            .ok_or("String too large")?;
        out.extend_from_slice(&(n | i32::from(n != 0)).to_le_bytes());
        out.extend_from_slice(s.as_bytes());
    } else {
        let latin = s.chars().all(|c| u32::from(c) <= 255);
        let units = i32::try_from(s.encode_utf16().count())
            .ok()
            .and_then(|n| n.checked_mul(2))
            .ok_or("String too large")?;
        out.extend_from_slice(&(units | i32::from(latin)).to_le_bytes());
        if latin {
            out.extend(s.chars().map(|c| c as u8));
        } else {
            for c in s.encode_utf16() {
                out.extend_from_slice(&c.to_le_bytes());
            }
        }
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn strings_match_length_profile_bytes() {
        for (text, bytes) in [
            (None, vec![255, 255, 255, 255]),
            (Some(""), vec![0, 0, 0, 0]),
            (Some("é"), vec![3, 0, 0, 0, 233]),
            (Some("💡"), vec![4, 0, 0, 0, 0x3d, 0xd8, 0xa1, 0xdc]),
        ] {
            let mut out = Vec::new();
            write_text(&mut out, text).unwrap();
            assert_eq!(out, bytes);
            let mut reader = Reader::new(&out, 2, 2);
            assert_eq!(reader.text().unwrap().as_deref(), text);
            assert!(reader.complete());
        }
    }

    #[test]
    fn readers_enforce_bounds_before_materializing_values() {
        assert!(Reader::new(&[2], 0, 0).boolean().is_err());
        assert!(Reader::new(&[1, 0, 0, 0], 4, 4).count(4).is_err());
        assert!(Reader::new(&[2, 0, 0, 0, 0, 0], 1, 2).count(1).is_err());
        assert!(Reader::new(&[3, 0, 0, 0, 65], 1, 0).text().is_err());
        assert!(Reader::new(&[2, 0, 0, 0, 0, 216], 1, 1).text().is_err());
    }
}
