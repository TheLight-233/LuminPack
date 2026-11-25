from array import array
import unittest

from luminpack import decode_list, encode_list


class PrimitiveTests(unittest.TestCase):
    def test_null_empty_and_signed(self):
        for value in [None, array("i"), array("i", [-2147483648, 1])]:
            self.assertEqual(decode_list(encode_list(value), "i"), value)

    def test_float_payload_bits(self):
        data = bytes.fromhex("020000003412c07f00000080")
        self.assertEqual(encode_list(decode_list(data, "f")), data)

    def test_invalid_length(self):
        for data in [b"", bytes.fromhex("ffffffff01"), bytes.fromhex("ffffff7f")]:
            with self.assertRaises(ValueError):
                decode_list(data, "i")
