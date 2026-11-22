package luminpack;

import java.util.ServiceConfigurationError;
import java.util.ServiceLoader;

/** Optional wide scans over already checked Binary string input. */
interface StringKernels {
    int firstNonAscii(byte[] data, int from, int to);
    int firstZeroByte(byte[] data, int from, int to);
    int firstZeroUtf16(byte[] data, int from, int to);

    static StringKernels load() {
        try {
            return ServiceLoader.load(StringKernels.class, StringKernels.class.getClassLoader()).findFirst().orElse(null);
        } catch (ServiceConfigurationError | LinkageError optionalProviderUnavailable) {
            return null;
        }
    }
}
