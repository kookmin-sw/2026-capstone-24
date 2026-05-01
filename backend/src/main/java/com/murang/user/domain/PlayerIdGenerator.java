package com.murang.user.domain;

import java.math.BigInteger;
import java.security.SecureRandom;
import java.time.Instant;
import java.util.regex.Pattern;

public final class PlayerIdGenerator {

    private static final char[] CROCKFORD_BASE32 =
            "0123456789ABCDEFGHJKMNPQRSTVWXYZ".toCharArray();
    private static final BigInteger BASE32 = BigInteger.valueOf(32L);
    private static final Pattern ULID_PATTERN = Pattern.compile("^[0-9A-HJKMNP-TV-Z]{26}$");
    private static final SecureRandom SECURE_RANDOM = new SecureRandom();

    private PlayerIdGenerator() {
    }

    public static String newPlayerId() {
        byte[] bytes = new byte[16];
        long timestamp = Instant.now().toEpochMilli();
        fillTimestamp(bytes, timestamp);

        byte[] randomness = new byte[10];
        SECURE_RANDOM.nextBytes(randomness);
        System.arraycopy(randomness, 0, bytes, 6, randomness.length);

        return encode(bytes);
    }

    public static boolean isUlid(String value) {
        return value != null && ULID_PATTERN.matcher(value).matches();
    }

    private static void fillTimestamp(byte[] bytes, long timestamp) {
        bytes[0] = (byte) (timestamp >>> 40);
        bytes[1] = (byte) (timestamp >>> 32);
        bytes[2] = (byte) (timestamp >>> 24);
        bytes[3] = (byte) (timestamp >>> 16);
        bytes[4] = (byte) (timestamp >>> 8);
        bytes[5] = (byte) timestamp;
    }

    private static String encode(byte[] bytes) {
        BigInteger value = new BigInteger(1, bytes);
        char[] encoded = new char[26];

        for (int index = encoded.length - 1; index >= 0; index--) {
            BigInteger[] divRem = value.divideAndRemainder(BASE32);
            encoded[index] = CROCKFORD_BASE32[divRem[1].intValue()];
            value = divRem[0];
        }

        return new String(encoded);
    }
}
