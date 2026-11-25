//! Headerless scalar roots and nullable Array/List roots of ordinary four-byte scalars.
mod sealed {
    pub trait Sealed {}
    impl Sealed for i32 {}
    impl Sealed for f32 {}
}

pub trait Primitive: sealed::Sealed + Copy {
    fn bytes(self) -> [u8; 4];
    fn from_bytes(bytes: [u8; 4]) -> Self;
}

macro_rules! primitive {
    ($t:ty) => {
        impl Primitive for $t {
            fn bytes(self) -> [u8; 4] {
                self.to_le_bytes()
            }
            fn from_bytes(bytes: [u8; 4]) -> Self {
                Self::from_le_bytes(bytes)
            }
        }
    };
}
primitive!(i32);
primitive!(f32);

pub fn decode_list<T: Primitive>(input: &[u8]) -> Result<Option<Vec<T>>, &'static str> {
    let (header, payload) = input
        .split_at_checked(4)
        .ok_or("Missing collection header")?;
    let n = i32::from_le_bytes(header.try_into().unwrap());
    if n == -1 && payload.is_empty() {
        return Ok(None);
    }
    if n < 0 || (n as usize).checked_mul(4) != Some(payload.len()) {
        return Err("Invalid collection payload length");
    }
    Ok(Some(
        payload
            .chunks_exact(4)
            .map(|v| T::from_bytes(v.try_into().unwrap()))
            .collect(),
    ))
}

pub fn encode_list<T: Primitive>(
    values: Option<&[T]>,
    output: &mut Vec<u8>,
) -> Result<(), &'static str> {
    let count = match values {
        None => -1,
        Some(v) => i32::try_from(v.len()).map_err(|_| "Collection too large")?,
    };
    let values = values.unwrap_or_default();
    let size = values
        .len()
        .checked_mul(4)
        .and_then(|n| n.checked_add(4))
        .ok_or("Collection too large")?;
    output.clear();
    output.reserve(size);
    output.extend_from_slice(&count.to_le_bytes());
    for value in values {
        output.extend_from_slice(&value.bytes());
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn collection_framing() {
        let mut out = vec![];
        for values in [None, Some(&[][..]), Some(&[i32::MIN, 42][..])] {
            encode_list(values, &mut out).unwrap();
            assert_eq!(decode_list::<i32>(&out).unwrap().as_deref(), values);
        }
        assert!(decode_list::<f32>(&out[..out.len() - 1]).is_err());
        let bits = 0x7fc01234;
        encode_list(Some(&[f32::from_bits(bits), -0.0]), &mut out).unwrap();
        assert_eq!(
            decode_list::<f32>(&out).unwrap().unwrap()[0].to_bits(),
            bits
        );
    }
}
