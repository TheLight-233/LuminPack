#![allow(nonstandard_style, dead_code, unused_imports)]
use serde::{de::DeserializeOwned, Deserialize, Serialize};
use std::{hint::black_box, time::Instant};

macro_rules! union_model {
 ($n:ident,$($t:ident=$hash:literal),+) => {
 pub enum $n {$($t($t)),+}
 impl Serialize for $n {
 fn serialize<S:serde::Serializer>(&self,s:S)->std::result::Result<S::Ok,S::Error>{
 use serde::ser::SerializeStruct;let mut a=s.serialize_struct("Union",2)?;
 match self {$(Self::$t(v)=>{a.serialize_field("$type",&($hash as u32))?;a.serialize_field("$value",v)?;}),+};a.end()}}
 impl<'de> Deserialize<'de> for $n{
 fn deserialize<D:serde::Deserializer<'de>>(d:D)->std::result::Result<Self,D::Error>{
 struct Visitor;
 impl<'de> serde::de::Visitor<'de> for Visitor {
 type Value=$n;
 fn expecting(&self,f:&mut std::fmt::Formatter)->std::fmt::Result{f.write_str("a Union envelope")}
 fn visit_seq<A:serde::de::SeqAccess<'de>>(self,mut a:A)->std::result::Result<$n,A::Error>{
 use serde::de::Error;match a.next_element::<u32>()?.ok_or_else(||Error::custom("tag"))?{
 $($hash=>Ok($n::$t(a.next_element()?.ok_or_else(||Error::custom("value"))?))),+, _=>Err(Error::custom("tag"))}}
 fn visit_map<A:serde::de::MapAccess<'de>>(self,mut a:A)->std::result::Result<$n,A::Error>{
 use serde::de::Error;if a.next_key::<String>()?.as_deref()!=Some("$type"){return Err(Error::custom("tag"))}
 let tag:u32=a.next_value()?;if a.next_key::<String>()?.as_deref()!=Some("$value"){return Err(Error::custom("value"))}
 match tag {$($hash=>Ok($n::$t(a.next_value()?))),+, _=>Err(Error::custom("tag"))}
 }}
 d.deserialize_struct("Union",&["$type","$value"],Visitor)}}};}
mod simple {
    include!(concat!(env!("OUT_DIR"), "/simple.rs"));
}
mod serialize {
    include!(concat!(env!("OUT_DIR"), "/serialize.rs"));
}
fn canon(v: &mut serde_json::Value) {
    match v {
        serde_json::Value::Array(a) => {
            for i in a.iter_mut() {
                canon(i)
            }
            if a.iter().all(|i| {
                i.as_array()
                    .is_some_and(|v| v.len() == 2 && v[0].as_i64().is_some())
            }) {
                a.sort_by_key(|i| i[0].as_i64())
            }
        }
        serde_json::Value::Object(o) => {
            for i in o.values_mut() {
                canon(i)
            }
        }
        _ => {}
    }
}
fn run<T: Serialize + DeserializeOwned>(
    m: &str,
    encode: fn(&Option<T>) -> luminpack::direct::Result<Vec<u8>>,
    decode: fn(&[u8]) -> luminpack::direct::Result<Option<T>>,
    jenc: for<'a> fn(
        &luminpack::Codec,
        &Option<T>,
        luminpack::Format,
        &'a mut [u8],
    ) -> std::result::Result<&'a [u8], String>,
    jdec: fn(
        &luminpack::Codec,
        &[u8],
        luminpack::Format,
        &mut [u8],
    ) -> std::result::Result<Option<T>, String>,
) {
    let dir = std::path::PathBuf::from(std::env::var_os("LP_MODELS").unwrap());
    let json = std::fs::read(dir.join(format!("{m}.utf8.json"))).unwrap();
    let bin = std::fs::read(dir.join(format!("{m}.bin"))).unwrap();
    let opt = Some(serde_json::from_slice::<T>(&json).unwrap());
    let v = opt.as_ref().unwrap();
    let mut expected = serde_json::to_value(&v).unwrap();
    canon(&mut expected);
    let check = |x: T| {
        let mut actual = serde_json::to_value(x).unwrap();
        canon(&mut actual);
        assert_eq!(actual, expected);
    };
    check(decode(&bin).unwrap().unwrap());
    check(decode(&encode(&opt).unwrap()).unwrap().unwrap());
    let codec = unsafe {
        luminpack::Codec::load(
            dir.join(format!("{m}.{}", std::env::consts::DLL_EXTENSION))
                .canonicalize()
                .unwrap(),
        )
        .unwrap()
    };
    let mut out = vec![0u8; 262144];
    let mut bs = out.clone();
    let f = luminpack::Format::Utf8;
    let canonical = jenc(&codec, &opt, f, &mut out).unwrap().to_vec();
    check(jdec(&codec, &canonical, f, &mut bs).unwrap().unwrap());
    check(jdec(&codec, &json, f, &mut bs).unwrap().unwrap());
    let mut jobs: Vec<(&str, &str, &str, usize, Box<dyn FnMut() + '_>)> = vec![];
    macro_rules! add {
        ($fmt:expr,$lib:expr,$encode:expr,$decode:expr) => {{
            let enc = $encode;
            let dec = $decode;
            let b = enc(&v);
            let size = b.len();
            check(dec(&b));
            let b = if $fmt == "json" { json.clone() } else { b };
            let input_size = b.len();
            check(dec(&b));
            let value = &v;
            jobs.push((
                $fmt,
                $lib,
                "encode",
                size,
                Box::new(move || {
                    black_box(enc(black_box(value)));
                }),
            ));
            jobs.push((
                $fmt,
                $lib,
                "decode",
                input_size,
                Box::new(move || {
                    black_box(dec(black_box(&b)));
                }),
            ));
        }};
    }
    add!(
        "binary",
        "bincode",
        |v: &T| bincode::serde::encode_to_vec(v, bincode::config::standard()).unwrap(),
        |b: &[u8]| bincode::serde::decode_from_slice::<T, _>(b, bincode::config::standard())
            .unwrap()
            .0
    );
    add!(
        "binary",
        "Postcard",
        |v: &T| postcard::to_allocvec(v).unwrap(),
        |b: &[u8]| postcard::from_bytes::<T>(b).unwrap()
    );
    add!(
        "binary",
        "rmp-serde",
        |v: &T| rmp_serde::to_vec(v).unwrap(),
        |b: &[u8]| rmp_serde::from_slice::<T>(b).unwrap()
    );
    add!(
        "json",
        "serde_json",
        |v: &T| serde_json::to_vec(v).unwrap(),
        |b: &[u8]| serde_json::from_slice::<T>(b).unwrap()
    );
    jobs.push((
        "binary",
        "LuminPack",
        "encode",
        bin.len(),
        Box::new(|| {
            black_box(encode(&opt).unwrap());
        }),
    ));
    jobs.push((
        "binary",
        "LuminPack",
        "decode",
        bin.len(),
        Box::new(|| {
            black_box(decode(&bin).unwrap().unwrap());
        }),
    ));
    jobs.push((
        "json",
        "LuminPack",
        "encode",
        canonical.len(),
        Box::new(|| {
            black_box(jenc(&codec, &opt, f, &mut out).unwrap().to_vec());
        }),
    ));
    jobs.push((
        "json",
        "LuminPack",
        "decode",
        json.len(),
        Box::new(|| {
            black_box(jdec(&codec, &json, f, &mut bs).unwrap().unwrap());
        }),
    ));
    for s in 0..10 {
        jobs.rotate_left(3);
        for (fmt, l, op, n, call) in &mut jobs {
            let t = Instant::now();
            let mut i = 0;
            while t.elapsed().as_millis() < 30 {
                for _ in 0..8 {
                    call()
                }
                i += 8
            }
            if s > 0 {
                println!(
                    "rust,{m},{fmt},{l},{op},{s},{},{},",
                    t.elapsed().as_nanos() as f64 / i as f64,
                    n
                )
            }
        }
    }
}
fn main() {
    println!("language,model,format,library,operation,sample,ns_per_op,bytes,alloc_bytes_per_op");
    run(
        "simple",
        simple::encode_binary,
        simple::decode_binary,
        simple::encode_json,
        simple::decode_json,
    );
    run(
        "serialize",
        serialize::encode_binary,
        serialize::decode_binary,
        serialize::encode_json,
        serialize::decode_json,
    );
}
