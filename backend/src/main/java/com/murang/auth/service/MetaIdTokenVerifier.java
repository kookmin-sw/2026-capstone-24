package com.murang.auth.service;

public interface MetaIdTokenVerifier {

    MetaIdentity verify(String metaIdToken);
}
