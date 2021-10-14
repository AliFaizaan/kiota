package com.microsoft.kiota;

import java.net.URI;
import java.net.URISyntaxException;
import java.io.IOException;
import java.io.InputStream;
import java.util.Arrays;
import java.util.Collection;
import java.util.HashMap;
import java.util.Objects;
import java.util.function.Function;

import javax.annotation.Nonnull;
import javax.annotation.Nullable;

import com.microsoft.kiota.serialization.Parsable;
import com.microsoft.kiota.serialization.SerializationWriter;
import com.microsoft.kiota.RequestAdapter;

/** This class represents an abstract HTTP request. */
public class RequestInformation {
    /** The URI of the request. */
    @Nullable
    public URI uri;
    /**
     * Sets the URI of the request.
     * @param currentPath the current path (scheme, host, port, path, query parameters) of the request.
     * @param pathSegment the segment to append to the current path.
     * @param isRawUrl whether the path segment is a raw url. When true, the segment is not happened and the current path is parsed for query parameters.
     */
    public void setUri(@Nullable final String currentPath, @Nullable final String pathSegment, final boolean isRawUrl) {
        if (isRawUrl) {
            if(currentPath == null || currentPath.isEmpty()) {
                throw new IllegalArgumentException("currentPath cannot be null or empty");
            }
            final var questionMarkSplat = currentPath.split("\\?");
            final var schemeHostAndPath = questionMarkSplat[0];
            this.setUriFromString(schemeHostAndPath);
            if (questionMarkSplat.length > 1) {
                final var queryString = questionMarkSplat[1];
                final var rawQueryParameters = queryString.split("&");
                for (var queryParameter : rawQueryParameters) {
                    final var queryParameterNameValue = queryParameter.split("=");
                    if (!queryParameterNameValue[0].isEmpty()) {
                        this.queryParameters.put(queryParameterNameValue[0], queryParameterNameValue.length > 1 ? queryParameterNameValue[1] : null);
                    }
                }
            }
        } else {
            this.setUriFromString(currentPath + pathSegment);
        }
    }
    private void setUriFromString(final String uriString) {
        try {
            this.uri = new URI(uriString);
        } catch (final URISyntaxException e) {
            throw new RuntimeException(e);
        }
    }
    /** The HTTP method for the request */
    @Nullable
    public HttpMethod httpMethod;
    /** The Query Parameters of the request. */
    @Nonnull
    public HashMap<String, Object> queryParameters = new HashMap<>(); //TODO case insensitive
    /** The Request Headers. */
    @Nonnull
    public HashMap<String, String> headers = new HashMap<>(); // TODO case insensitive
    /** The Request Body. */
    @Nullable
    public InputStream content;
    private HashMap<String, RequestOption> _requestOptions = new HashMap<>();
    /**
     * Gets the request options for this request. Options are unique by type. If an option of the same type is added twice, the last one wins.
     * @return the request options for this request.
     */
    public Collection<RequestOption> getRequestOptions() { return _requestOptions.values(); }
    /**
     * Adds a request option to this request.
     * @param option the request option to add.
     */
    public void addRequestOptions(@Nullable final RequestOption... options) { 
        if(options == null || options.length == 0) return;
        for(final RequestOption option : options) {
            _requestOptions.put(option.getClass().getCanonicalName(), option);
        }
    }
    /**
     * Removes a request option from this request.
     * @param option the request option to remove.
     */
    public void removeRequestOptions(@Nullable final RequestOption... options) {
        if(options == null || options.length == 0) return;
        for(final RequestOption option : options) {
            _requestOptions.remove(option.getClass().getCanonicalName());
        }
    }
    private static String binaryContentType = "application/octet-stream";
    private static String contentTypeHeader = "Content-Type";
    /**
     * Sets the request body to be a binary stream.
     * @param value the binary stream
     */
    public void setStreamContent(@Nonnull final InputStream value) {
        Objects.requireNonNull(value);
        this.content = value;
        headers.put(contentTypeHeader, binaryContentType);
    }
    /**
     * Sets the request body from a model with the specified content type.
     * @param values the models.
     * @param contentType the content type.
     * @param requestAdapter The adapter service to get the serialization writer from.
     * @param <T> the model type.
     */
    public <T extends Parsable> void setContentFromParsable(@Nonnull final RequestAdapter requestAdapter, @Nonnull final String contentType, @Nonnull final T... values) {
        Objects.requireNonNull(requestAdapter);
        Objects.requireNonNull(values);
        Objects.requireNonNull(contentType);
        if(values.length == 0) throw new RuntimeException("values cannot be empty");

        try(final SerializationWriter writer = requestAdapter.getSerializationWriterFactory().getSerializationWriter(contentType)) {
            headers.put(contentTypeHeader, contentType);
            if(values.length == 1) 
                writer.writeObjectValue(null, values[0]);
            else
                writer.writeCollectionOfObjectValues(null, Arrays.asList(values));
            this.content = writer.getSerializedContent();
        } catch (IOException ex) {
            throw new RuntimeException("could not serialize payload", ex);
        }
    }
}
