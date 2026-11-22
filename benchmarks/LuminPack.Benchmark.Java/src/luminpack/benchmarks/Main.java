package luminpack.benchmarks;

import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Arrays;
import java.util.HashMap;
import java.util.List;
import java.util.Locale;
import java.util.concurrent.TimeUnit;
import java.util.regex.Pattern;

import org.openjdk.jmh.annotations.Mode;
import org.openjdk.jmh.profile.GCProfiler;
import org.openjdk.jmh.runner.Runner;
import org.openjdk.jmh.runner.options.OptionsBuilder;
import org.openjdk.jmh.runner.options.TimeValue;

/** Validates the fixtures, executes JMH and writes individual measurement iterations to CSV. */
public final class Main {
    private record Case(String model, CodecBenchmarks.Serializer serializer) {}

    private Main() {}

    public static void main(String[] args) throws Exception {
        if (args.length < 2) throw new IllegalArgumentException("Expected the models directory and CSV output path.");
        Path models = Path.of(args[0]).toAbsolutePath();
        List<String> options = Arrays.asList(args).subList(2, args.length);
        boolean quick = options.contains("quick");
        var byteSizes = new HashMap<Case, Integer>();
        for (String model : List.of("simple", "serialize")) {
            for (CodecBenchmarks.Serializer serializer : CodecBenchmarks.Serializer.values()) {
                var prepared = CodecBenchmarks.prepare(models, model, serializer);
                byteSizes.put(new Case(model, serializer), prepared.payload().length);
            }
        }
        System.out.printf("Verified %d Java model and serializer combinations.%n", byteSizes.size());
        if (options.contains("verify")) return;

        var configuration = new OptionsBuilder().include("^" + Pattern.quote(CodecBenchmarks.class.getName()) + "\\.(encode|decode)$")
            .mode(Mode.AverageTime).timeUnit(TimeUnit.NANOSECONDS).threads(1)
            .warmupIterations(quick ? 2 : 3).warmupTime(TimeValue.milliseconds(quick ? 200 : 500))
            .measurementIterations(quick ? 3 : 5).measurementTime(TimeValue.milliseconds(quick ? 200 : 500))
            .forks(quick ? 1 : 2).shouldFailOnError(true).addProfiler(GCProfiler.class)
            .jvmArgsAppend("-Xms256m", "-Xmx256m", "-D" + CodecBenchmarks.MODELS_PROPERTY + "=" + models).build();
        var results = new Runner(configuration).run();
        if (results.size() != byteSizes.size() * 2) throw new IllegalStateException("Incomplete Java benchmark matrix.");

        try (var writer = Files.newBufferedWriter(Path.of(args[1]))) {
            writer.write("language,model,format,library,operation,sample,ns_per_op,bytes,alloc_bytes_per_op\n");
            for (var result : results) {
                var parameters = result.getParams();
                String model = parameters.getParam("model");
                var serializer = CodecBenchmarks.Serializer.valueOf(parameters.getParam("serializer"));
                String benchmark = parameters.getBenchmark();
                String operation = benchmark.substring(benchmark.lastIndexOf('.') + 1);
                int bytes = byteSizes.get(new Case(model, serializer));
                int sample = 0;
                for (var fork : result.getBenchmarkResults()) {
                    for (var iteration : fork.getIterationResults()) {
                        var allocation = iteration.getSecondaryResults().get("gc.alloc.rate.norm");
                        String allocationBytes = allocation == null || !Double.isFinite(allocation.getScore()) ? "" :
                            String.format(Locale.ROOT, "%.3f", allocation.getScore());
                        writer.write(String.format(Locale.ROOT, "java,%s,%s,%s,%s,%d,%.3f,%d,%s%n", model,
                            serializer.format, serializer.displayName, operation, ++sample, iteration.getPrimaryResult().getScore(),
                            bytes, allocationBytes));
                    }
                }
                if (sample != (quick ? 3 : 10)) throw new IllegalStateException("Incomplete Java samples for " + benchmark);
            }
        }
    }
}
